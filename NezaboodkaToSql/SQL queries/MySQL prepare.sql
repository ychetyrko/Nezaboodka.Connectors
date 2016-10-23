/*********************************************
	Create Nezaboodka administrative database
		and grant all rights to nezaboodka user for it
**********************************************/

CREATE DATABASE IF NOT EXISTS `nezaboodka_admin`;
USE `nezaboodka_admin`;

/*********************************************
		Preparing administrative table
**********************************************/

/*********************************************
	Databases list to store user rights:
		0 - ReadWrite
		1 - ReadOnly
        2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) NOT NULL UNIQUE PRIMARY KEY,
	`access` TINYINT DEFAULT 0	/* ReadWrite access by default for any new database */
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
	Trigger on creating database
*/

/*********************************************
	Stored procedures to effectively
		add/remove databases
*/
DELIMITER //

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
		
        SET @prepStr=CONCAT('DROP DATABASE IF EXISTS ', db_name, ';');
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
        
		SET @prepStr=CONCAT('CREATE DATABASE ', db_name, ';');
        PREPARE procPrep FROM @prepStr;
        EXECUTE procPrep;
        DEALLOCATE PREPARE procPrep;
            
        SET @prepStr=CONCAT('INSERT INTO `db_list` (`name`) value (\'', db_name, '\');');
        PREPARE procPrep FROM @prepStr;
        EXECUTE procPrep;
        DEALLOCATE PREPARE procPrep;
    END LOOP;
    
    CLOSE cur;
	
    TRUNCATE TABLE db_add_list;
END //

CREATE PROCEDURE alter_database_list ()
BEGIN
	CALL remove_databases();
    CALL add_databases();
END //

DELIMITER ;

/*********************************************
	TODO: everything...
*/



/*********************************************
	Create Nezaboodka users
		and grant rights for databases
*/

CREATE USER 'nezaboodka_admin'@'%' IDENTIFIED BY  'nezaboodka' password expire;
GRANT ALL ON *.* TO 'nezaboodka_admin'@'%';

/*	Localhost users	*/
CREATE USER 'nezaboodka_admin'@'localhost' IDENTIFIED BY  'nezaboodka' password expire;
GRANT ALL ON *.* TO 'nezaboodka_admin'@'localhost';

FLUSH PRIVILEGES;
