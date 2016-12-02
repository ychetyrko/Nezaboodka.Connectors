/*********************************************

	Create Nezaboodka administrative database
		and grant all rights to nezaboodka user for it
        
**********************************************/

DROP DATABASE IF EXISTS `nz_admin_db`;

CREATE DATABASE `nz_admin_db`;
USE `nz_admin_db`;

/*********************************************

	Prepare administrative database tables
        
**********************************************/

/*********************************************
	Databases list to store user rights:
		0 - ReadWrite
		1 - ReadOnly
        2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE,
	`access` TINYINT DEFAULT 0
    /* set 'ReadWrite' access by default for any new database */
);

/*********************************************
	List of databases to add
*/
CREATE TABLE `db_add_list`(
	`name` VARCHAR(64) NOT NULL UNIQUE
);

/*********************************************
	List of databases to remove
*/
CREATE TABLE `db_rem_list`(
	`name` VARCHAR(64) NOT NULL UNIQUE
);


/*********************************************

		Stored procedures and functions
        
**********************************************/

DELIMITER //

/*********************************************
	Prepare empty database for work
*/
CREATE PROCEDURE prepare_db(db_name VARCHAR(64))
BEGIN
	
	# Class
	SET @prepStr=CONCAT('
		CREATE TABLE `', db_name, '`.`type` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`name` VARCHAR(128) NOT NULL UNIQUE,
			`table_name` VARCHAR(64) NOT NULL UNIQUE,
			`base_type_name` VARCHAR(128) NOT NULL,
			`base_type_id` INT DEFAULT NULL,
            
			FOREIGN KEY(`base_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE
		);
	');
	PREPARE procPrep FROM @prepStr;
	EXECUTE procPrep;
	DEALLOCATE PREPARE procPrep;
    
    # Field
    SET @prepStr=CONCAT('
		CREATE TABLE `', db_name, '`.`field` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_type_name` VARCHAR(128) NOT NULL,
			`owner_type_id` INT DEFAULT NULL,
			`name` VARCHAR(128) NOT NULL,
			`col_name` VARCHAR(64) NOT NULL,
			`type_name` VARCHAR(64) DEFAULT NULL,
			`ref_type_id` INT DEFAULT NULL,
			`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
			`compare_options` ENUM
				(
					\'None\',
					\'IgnoreCase\',
					\'IgnoreNonSpace\',
					\'IgnoreSymbols\',
					\'IgnoreKanaType\',
					\'IgnoreWidth\',
					\'OrdinalIgnoreCase\',
					\'StringSort\',
					\'Ordinal\'
				) NOT NULL DEFAULT \'None\',
			`back_ref_name` VARCHAR(128) DEFAULT NULL,
			`back_ref_id` INT DEFAULT NULL,
            
			FOREIGN KEY(`owner_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
                
			FOREIGN KEY(`ref_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE SET NULL,
                
			FOREIGN KEY(`back_ref_id`)
				REFERENCES `field`(`id`)
				ON DELETE SET NULL
		);
    ');
    PREPARE procPrep FROM @prepStr;
	EXECUTE procPrep;
	DEALLOCATE PREPARE procPrep;
    
    # Type-Field optimization
    SET @prepStr=CONCAT('
		CREATE TABLE `', db_name, '`.`type_field_map` (
			`type_id` INT NOT NULL,
			`field_id` INT NOT NULL,
            
			FOREIGN KEY (`type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
                
			FOREIGN KEY (`field_id`)
				REFERENCES `field`(`id`)
				ON DELETE CASCADE
		);
	');
	PREPARE procPrep FROM @prepStr;
	EXECUTE procPrep;
	DEALLOCATE PREPARE procPrep;
    
    
    # DbKey
    SET @prepStr=CONCAT('
		CREATE TABLE `', db_name, '`.`db_key` (
			`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`raw_rev` BIGINT(0) NOT NULL DEFAULT 1,
			`real_type_id` INT NOT NULL,
            
			FOREIGN KEY (`real_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE
		);
	');
	PREPARE procPrep FROM @prepStr;
	EXECUTE procPrep;
	DEALLOCATE PREPARE procPrep;
    
END //


/*********************************************
	Effectively alter database list
*/
CREATE PROCEDURE remove_databases ()
BEGIN
	DECLARE done INT DEFAULT FALSE;
    DECLARE db_name VARCHAR(64);
    DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_rem_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    OPEN cur;
    
    proc_loop: LOOP
		FETCH cur INTO db_name;
        IF done THEN
			LEAVE proc_loop;
		END IF;
		
        SET @prepStr=CONCAT('
			DROP DATABASE IF EXISTS ', db_name, ';
        ');
		PREPARE procPrep FROM @prepStr;
		EXECUTE procPrep;
		DEALLOCATE PREPARE procPrep;
        
        DELETE FROM db_list
        WHERE `name` = db_name;
    END LOOP;
    
    CLOSE cur;
	
    TRUNCATE TABLE db_rem_list;
END //

CREATE PROCEDURE add_databases ()
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE db_name VARCHAR(64);
    DECLARE db_exists INT DEFAULT 0;
    DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_add_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    OPEN cur;
    
    proc_loop: LOOP
		FETCH cur INTO db_name;
        IF done THEN
			LEAVE proc_loop;
		END IF;
		
        # Using prepared statement argument as database name is restricted => 
        SET @prepStr=CONCAT('
			CREATE DATABASE `', db_name, '`;
        ');
		PREPARE p_create_db FROM @prepStr;
        EXECUTE p_create_db;
        DEALLOCATE PREPARE p_create_db;
        
        CALL prepare_db(db_name);
        
        # Inserting values in prepared statement is not available yet =>
        SET @prepStr=CONCAT('
			INSERT INTO `db_list` (`name`) value (\'', db_name ,'\');
        ');
		PREPARE p_add_to_db FROM @prepStr;
        EXECUTE p_add_to_db;
        DEALLOCATE PREPARE p_add_to_db;
    END LOOP;
    
    CLOSE cur;
	
    TRUNCATE TABLE db_add_list;
END //

CREATE PROCEDURE alter_database_list()
BEGIN
	CALL remove_databases();
    CALL add_databases();
    
    # get database list
    SELECT `name` FROM `db_list`;
END //

/*
	Protocol for altering database list:
    
	1. fill `db_rem_list` and `db_add_list` tables with database names;
	2. call `alter_database_list`
		(or add_databases / remove_databases separately)
	
**********************************************/

/*********************************************
	Alter database schema support tables (`type`, `field`)
*/

CREATE PROCEDURE prepare_alter_proc_for_db(db_name VARCHAR(64))
BEGIN
	
    SET @prepStr = CONCAT('
		SELECT COUNT(`id`) FROM `', db_name ,'`.`type`
		INTO @types_count;
	');
    PREPARE p_get_types_count FROM @prepStr;
    
	EXECUTE p_get_types_count;
    
    # Get type id and name -> @cur_type_id, @cur_type_name
	SET @prepStr = CONCAT('
		SELECT `id`, `name`
		FROM `', db_name ,'`.`type`
		LIMIT ?, 1
		INTO @cur_type_id, @cur_type_name;
	');
	PREPARE p_get_type_id_name FROM @prepStr;
    
    # Get type id and table name -> @cur_type_id, @cur_table_name
	SET @prepStr = CONCAT('
		SELECT `id`, `table_name`
		FROM `', db_name ,'`.`type`
		LIMIT ?, 1
		INTO @cur_type_id, @cur_table_name;
	');
	PREPARE p_get_type_id_tablename FROM @prepStr;
    
    # Update base type id-s
	SET @prepStr = CONCAT('
		UPDATE `', db_name, '`.`type`
		SET `base_type_id` = ?
		WHERE `base_type_name` = ?;
	');
	PREPARE p_update_type FROM @prepStr;
    
    # Update owner type id
	SET @prepStr = CONCAT('
		UPDATE `', db_name, '`.`field`
		SET `owner_type_id` = ?
		WHERE `owner_type_name` = ?;
	');
	PREPARE p_update_fields_type FROM @prepStr;
    
    # Update fields' ref type id (if ref type)
	SET @prepStr = CONCAT('
		UPDATE `', db_name, '`.`field`
		SET `ref_type_id` = ?
		WHERE `type_name` = ?;
	');
	PREPARE p_update_fields_ref FROM @prepStr;
    
    # Type-Field optimization table filling
    SET @prepStr = CONCAT('
		INSERT INTO `', db_name, '`.`type_field_map` (`type_id`, `field_id`)
			SELECT ?, `id`
			FROM `', db_name, '`.`field`
			WHERE `id` = ?;
	');
	PREPARE p_map_base_type_fields FROM @prepStr;
	
	# Get base type id to continue filling
	SET @prepStr = CONCAT('
		SELECT `base_type_id`
		FROM `', db_name ,'`.`type`
		WHERE `id` = ?
		LIMIT 1
        INTO @bi;
	');
	PREPARE p_next_base_type FROM @prepStr;
    
    # Get fields count by type id -> @fields_count
	SET @prepStr = CONCAT('
        SELECT COUNT(`field_id`)
		FROM `', db_name ,'`.`type_field_map`
        WHERE `type_id` = ?
        INTO @fields_count;
	');
    PREPARE p_get_fields_count FROM @prepStr;
    
END //

CREATE PROCEDURE deallocate_alter_proc()
BEGIN
	DEALLOCATE PREPARE p_get_types_count;
    
    DEALLOCATE PREPARE p_get_type_id_name;
    DEALLOCATE PREPARE p_get_type_id_tablename;
    
    DEALLOCATE PREPARE p_update_type;
    
    DEALLOCATE PREPARE p_update_fields_type;
    DEALLOCATE PREPARE p_update_fields_ref;
    
    DEALLOCATE PREPARE p_map_base_type_fields;
    DEALLOCATE PREPARE p_next_base_type;
    
    DEALLOCATE PREPARE p_get_fields_count;
END //

CREATE PROCEDURE alter_table_for_type(db_name VARCHAR(64), type_no INT)
BEGIN
	DECLARE fields_defs VARCHAR(255) DEFAULT "";
	
    SET @t_no = type_no;
    EXECUTE p_get_type_id_tablename USING @t_no;
    
    SELECT @cur_type_id as 'Type Id', @cur_table_name AS 'Table name';
    
    EXECUTE p_get_fields_count USING @cur_type_id;
    
    SELECT @cur_type_id as 'count';
    
    /*	Field table columns
		`id` INT,
		`name` VARCHAR(128),
		`col_name` VARCHAR(64),
		`type_name` VARCHAR(64),
		`ref_type_id` INT,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM
			(
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
		`back_ref_id` INT DEFAULT NULL,
    */
    
# TODO: move to preparation routine
    # Get field info by type id and field number in table
	SET @prepStr = CONCAT('
		SELECT `col_name`
        FROM `', db_name ,'`.`field`
        WHERE `id` = (
			SELECT `field_id`
			FROM `', db_name ,'`.`type_field_map`
			WHERE `type_id` = ?
			LIMIT ?, 1
		)
        LIMIT 1
        INTO @cur_field_col_name;
	');
    PREPARE p_get_field FROM @prepStr;
    
    
/* ***** Main cycle on fields ****** */
    
    SET @fi = 0;
    f_loop: LOOP 
		
        IF @fi = @fields_count THEN
			LEAVE f_loop;
        END IF;
		
		EXECUTE p_get_field USING @cur_type_id, @fi;

# TODO: Concat fields definitions (-> fields_defs) to prepare and execute later
		SET fields_defs = CONCAT(fields_defs, ',',
			@cur_field_col_name, ' INT
		');

# TODO: Concat constraints setters to prepare later
        
        SET @fi = @fi + 1;
	END LOOP f_loop;
    
    
    # Create table for type with all fields
    #  (table name can't be a parameter => prepare each time)
    SET @prepStr = CONCAT('
		CREATE TABLE `', db_name ,'`.`', @cur_table_name, '` (
			id BIGINT(0) PRIMARY KEY NOT NULL
            
			', fields_defs ,',	

			FOREIGN KEY (id)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE
        );
	');
	PREPARE p_create_table FROM @prepStr;
	EXECUTE p_create_table;
    
	DEALLOCATE PREPARE p_create_table;
    
    DEALLOCATE PREPARE p_get_field;
    
END //

CREATE PROCEDURE alter_db_schema(db_name VARCHAR(64))
BEGIN

	CALL prepare_alter_proc_for_db(db_name);
    
/* ***** Main cycle on types ****** */

    SET @ti = 0;
    typeLoop: LOOP
    
		IF @ti = @types_count THEN
			LEAVE typeLoop;
		END IF;
        
        EXECUTE p_get_type_id_name USING @ti;
		#SELECT @ti as 'No', @cur_type_id as 'id', @cur_type_name as 'name';
        
        EXECUTE p_update_type USING @cur_type_id, @cur_type_name;
        EXECUTE p_update_fields_type USING @cur_type_id, @cur_type_name;
        EXECUTE p_update_fields_ref USING @cur_type_id, @cur_type_name;
        
        SET @ti = @ti + 1;
	END LOOP typeLoop;
    
    
/* ***** Cycle on fields ****** */

    SET @ti = 0;
    typeLoop_2: LOOP
    
		IF @ti = @types_count THEN
			LEAVE typeLoop_2;
		END IF;
        
        EXECUTE p_get_type_id_name USING @ti;
        
        SET @bi = @cur_type_id;
        fields_loop: LOOP
			
			IF @bi IS NULL THEN
				LEAVE fields_loop;
			END IF;
            
            #SELECT @bi as 'base id';
            
			EXECUTE p_map_base_type_fields USING @cur_type_id, @bi;
			EXECUTE p_next_base_type USING @bi;
            
		END LOOP fields_loop;
        
        SET @ti = @ti + 1;
	END LOOP typeLoop_2;
    
    
# TODO:  2nd fields cycle to set back_ref_id

    
/* ***** Create table for each type ****** */
    
    SET @ti = 0;
    tables_loop: LOOP
    
		IF @ti = @types_count THEN
			LEAVE tables_loop;
		END IF;
        
		CALL alter_table_for_type(db_name, @ti);
        
        SET @ti = @ti + 1;
    END LOOP tables_loop;
    
# TODO: Set Constraints on fields of all tables
#  (prepare and execute agregated string, formed during creating tables)
    
    CALL deallocate_alter_proc();
END //

/*
	Protocol for altering database schema:
    
	1. fill `type` and `field` tables with appropriate data;
	2. call `alter_db_schema`
	
**********************************************/


/*********************************************
	Get table for db_key -> @t_table_name
*/
CREATE PROCEDURE get_type_table(db_name VARCHAR(64), db_key BIGINT(0))
BEGIN
	SET @prepStr = CONCAT('
		SELECT `table_name`
		FROM `', db_name, '`.`type`
		WHERE `id` = (
			SELECT `real_type_id`
			FROM `', db_name, '`.`db_key`
			WHERE `sys_id` = db_key
			LIMIT 1
		)
		LIMIT 1;
		INTO @t_table_name;
	');
	PREPARE p_get_type_table FROM @prepStr;
	EXECUTE p_get_type_table;
	DEALLOCATE PREPARE p_get_type_table;
END //

/*********************************************
	TODO: other stored procedures...
*/

DELIMITER ;

/******************************************************************************/

/*********************************************

		Create Nezaboodka users
			and grant rights for databases
        
**********************************************/

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

/*	Localhost user	*/
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
