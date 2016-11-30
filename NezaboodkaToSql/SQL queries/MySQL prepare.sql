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
	
	# Classes
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
    
    # Fields
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
CREATE PROCEDURE alter_db_schema(db_name VARCHAR(64))
BEGIN
	DECLARE done INT DEFAULT FALSE;
    DECLARE type_id INT DEFAULT NULL;
    DECLARE type_name VARCHAR(128);
    DECLARE base_type_id INT DEFAULT NULL;
    DECLARE i INT DEFAULT 0;
	
    SET @prepStr = CONCAT('
		SELECT COUNT(`id`) FROM `', db_name ,'`.`type`
		INTO @typesCount;
	');
    PREPARE p_get_types_count FROM @prepStr;
	EXECUTE p_get_types_count;
	DEALLOCATE PREPARE p_get_types_count;
    
    SET @ti = 0;
    typeLoop: LOOP
    
		IF @ti = @typesCount THEN
			LEAVE typeLoop;
		END IF;
        
        # Get type id -> @cur_type_id
		SET @prepStr = CONCAT('
			SET @cur_type_id = (
				SELECT `id`
				FROM `', db_name ,'`.`type`
				LIMIT ', @ti,', 1
			);
        ');
        PREPARE p_get_type_id FROM @prepStr;
        EXECUTE p_get_type_id;
        DEALLOCATE PREPARE p_get_type_id;
		
        /* DEBUG */
		#	SELECT @ti as 'No', @cur_type_id;
        /* ^ DEBUG */
        
        
        # Get type name -> @cur_type_name
        SET @prepStr = CONCAT('
			SET @cur_type_name = (
				SELECT `name`
				FROM `', db_name ,'`.`type`
				WHERE `id`=@cur_type_id
			);
        ');
        PREPARE p_get_type_name FROM @prepStr;
        EXECUTE p_get_type_name;
        DEALLOCATE PREPARE p_get_type_name;
        
        /* DEBUG */
		#	SELECT @ti as 'No', @cur_type_name as 'name';
        /* ^ DEBUG */
		
        # Update base type id-s
        SET @prepStr = CONCAT('
			UPDATE `', db_name, '`.`type`
			SET `base_type_id` = @cur_type_id
			WHERE `base_type_name` = @cur_type_name;
        ');
        PREPARE p_update_type FROM @prepStr;
        EXECUTE p_update_type;
        DEALLOCATE PREPARE p_update_type;
		
        
        # Update owner type id
        SET @prepStr = CONCAT('
			UPDATE `', db_name, '`.`field`
			SET `owner_type_id` = @cur_type_id
			WHERE `owner_type_name` = @cur_type_name;
        ');
        PREPARE p_update_fields_type FROM @prepStr;
        EXECUTE p_update_fields_type;
        DEALLOCATE PREPARE p_update_fields_type;
        
        
        # Update fields' ref type id (if ref type)
        SET @prepStr = CONCAT('
			UPDATE `', db_name, '`.`field`
			SET `ref_type_id` = @cur_type_id
			WHERE `type_name` = @cur_type_name;
        ');
        PREPARE p_update_fields_ref FROM @prepStr;
        EXECUTE p_update_fields_ref;
        DEALLOCATE PREPARE p_update_fields_ref;
        
        
        # Type-Field Optimization table fill
        SET @bi = @cur_type_id;
        fields_loop: LOOP
        
			IF @bi IS NULL THEN
				LEAVE fields_loop;
			END IF;
            
            SET @prepStr = CONCAT('
				INSERT INTO `', db_name, '`.`type_field_map` (`type_id`, `field_id`)
					SELECT @cur_type_id, `id`
					FROM `', db_name, '`.`field`
					WHERE `id` = @bi;
            ');
			PREPARE p_base_type_id FROM @prepStr;
			EXECUTE p_base_type_id;
			DEALLOCATE PREPARE p_base_type_id;
            
            SET @prepStr = CONCAT('
				SET @bi = (
					SELECT `base_type_id`
					FROM `', db_name ,'`.`type`
					WHERE `id` = @bi
                    LIMIT 1
				);
			');
			PREPARE p_next_base_type FROM @prepStr;
			EXECUTE p_next_base_type;
			DEALLOCATE PREPARE p_next_base_type;
            
		END LOOP fields_loop;
        
        SET @ti = @ti + 1;
	END LOOP typeLoop;
    
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
		SET @t_table_name = (
			SELECT `table_name`
			FROM `', db_name, '`.`type`
			WHERE `id` = (
				SELECT `real_type_id`
				FROM `', db_name, '`.`db_key`
				WHERE `sys_id` = db_key
				LIMIT 1
			)
			LIMIT 1;
		);
	');
	PREPARE p_get_ FROM @prepStr;
	EXECUTE p_get_type_id;
	DEALLOCATE PREPARE p_get_type_id;
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
